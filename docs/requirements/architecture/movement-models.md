# Requirements: Movement Models

## Purpose

CSWUnity shall provide interchangeable movement models for camera navigation
and other host-controlled rigs. Input, movement policy, map coordinates,
surface queries, constraints, and Unity camera application shall be separable.

## Scope

This document covers the common movement contract, the required movement models,
extensibility, and movement diagnostics and tests. This document holds the
Principle, Mechanism, and Class requirements of the area. Its Behavior
requirements are in
[Movement Models Behavior](../functional/movement-models.md).

## Common movement contract

### REQ-CSWU-MOVE-001: Separated responsibilities

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** None
- **Verification:** Design review of the separated movement responsibilities

The movement architecture shall separate:

- input source;
- movement model;
- coordinate service;
- surface/spatial query service;
- constraints;
- camera or object rig; and
- orchestration and timing.

### REQ-CSWU-MOVE-002: Input independence

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M3
- **Use cases:** UC-CSWU-011
- **Verification:** EditMode test with immutable input frames and no Unity input
  API

A movement model shall consume an immutable input frame rather than call a
Unity input API directly.

### REQ-CSWU-MOVE-003: Deterministic step

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M3
- **Use cases:** UC-CSWU-011, UC-CSWU-013
- **Verification:** Replay test reproducing the pose sequence within tolerance

A movement model shall advance from explicit state, input, time step,
coordinate context, and available query samples. The same inputs shall support
live operation, replay, and automated testing.

### REQ-CSWU-MOVE-004: Pose convention

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** UC-CSWU-161
- **Verification:** Coordinate tests of the pose convention and rig mapping

The authoritative pose shall use a double-precision Global 3D Position and a
documented orientation in the active Global 3D Frame. Localization shall produce
a Local 3D Position for the active frame. At the rig boundary, the CSWUnity
adapter maps that value to the specifically named Unity position required by
the target transform.

### REQ-CSWU-MOVE-006: Constraints

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-122, UC-CSWU-130, UC-CSWU-131, UC-CSWU-132
- **Verification:** Tests composing constraints with each required movement
  model

Clearance, collision, slope, pitch, speed, acceleration, bounds, and other
policies shall be composable constraints rather than duplicated inside every
movement model.

### REQ-CSWU-MOVE-007: Async query use

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** UC-CSWU-003, UC-CSWU-133, UC-CSWU-160
- **Verification:** Test proving no main-thread blocking while terrain tiles
  load

Movement models shall not block the Unity main thread while waiting for ground,
building, or streamed-map data.

## Extensibility

### REQ-CSWU-MOVE-012: Custom model

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-011
- **Verification:** Test adding a custom movement model without changing input,
  coordinate, query, or rig code

A host shall be able to add a movement model and parameter profile without
modifying the input, coordinate, query, or rig implementations.

### REQ-CSWU-MOVE-013: Input adapters

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-011, UC-CSWU-013
- **Verification:** Tests with live, recorded, and automated input adapters

Live devices, Unity Input System actions, legacy input, network commands,
recordings, and automated tests shall be addable as input sources.

### REQ-CSWU-MOVE-014: Rig adapters

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** None
- **Verification:** Test driving a camera rig and an object rig with one
  movement model

Movement models shall be usable with a Unity camera rig and with a
host-controlled object rig when their pose and constraint capabilities match.

### REQ-CSWU-MOVE-015: Object follow

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-150, UC-CSWU-151, UC-CSWU-152
- **Verification:** Test following a moving object across a Local Origin Offset
  rebase

The architecture shall allow an optional model or constraint that follows an
external world object while preserving Map Coordinate Context and Local 3D
Frame semantics.

## Diagnostics and tests

### REQ-CSWU-MOVE-017: Test paths

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** UC-CSWU-014
- **Verification:** Review of the published test paths and tolerances

Each supported model shall provide deterministic test paths and acceptance
tolerances for position, orientation, clearance, and coordinate transitions.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Movement Models Behavior](../functional/movement-models.md), is verified when
the following acceptance criteria hold:

- Free-flight, geodetic orbit, and flying-carpet models use the same rig,
  coordinate, timing, and input contracts.
- A recorded input stream reproduces the same authoritative pose sequence
  within tolerance.
- Flying-carpet movement remains non-blocking while terrain tiles load and
  follows its configured unavailable-data policy.
- Switching movement models preserves pose without an unintended coordinate or
  Local Origin Offset jump.
- Movement remains correct on representative UTM, geodetic, and geocentric
  maps.

## Related Documentation

- [Movement Models Behavior requirements](../functional/movement-models.md)
- [Movement architecture](../../design/movement-models.md)
- [Scene control](../functional/scene-control.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Camera and movement use cases](../use-cases/camera.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
