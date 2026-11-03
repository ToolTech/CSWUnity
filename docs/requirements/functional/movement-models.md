# Requirements: Movement Models Behavior

## Purpose

This document holds the Behavior requirements of the
[Movement Models](../architecture/movement-models.md) area. The purpose, scope,
and verification of the area are described in that document.

## Scope

The scope of the area is described in
[Movement Models](../architecture/movement-models.md#scope). This document
contains requirements of the following groups:

- Common movement contract
- Required models
- Diagnostics and tests

## Common movement contract

### REQ-CSWU-MOVE-005: Runtime selection

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-011
- **Verification:** Test switching movement models at runtime without replacing
  SceneManager and without a pose jump

The host shall be able to select and change movement models through serialized
profiles or runtime control without replacing the CSW SceneManager.

## Required models

### REQ-CSWU-MOVE-008: Free-flight model

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-115
- **Verification:** Deterministic free-flight test path

CSWUnity shall provide a six-degree or configurable free-flight model with
explicit speed, acceleration, damping, local-up, and rotation policies.

### REQ-CSWU-MOVE-009: Geodetic orbit model

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-111, UC-CSWU-112, UC-CSWU-113
- **Verification:** Deterministic pan, orbit, and zoom test paths on geodetic
  and geocentric maps

CSWUnity shall provide ellipsoid-aware pan, orbit, and zoom behavior using
local east/north/up orientation and a configurable focus or surface anchor.

### REQ-CSWU-MOVE-010: Flying-carpet model

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-003, UC-CSWU-133
- **Verification:** Deterministic ground-following test paths, including
  unavailable surface data

CSWUnity shall provide a ground-following model that:

- maintains configurable target and minimum clearance;
- samples ground or selected surfaces asynchronously;
- supports smooth vertical and orientation response;
- can use local up or optional surface-normal alignment;
- defines speed, acceleration, and turn behavior; and
- applies a deterministic policy when surface data is unavailable.

### REQ-CSWU-MOVE-011: Unavailable-surface policy

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-003, UC-CSWU-133
- **Depends on:** REQ-CSWU-MOVE-010
- **Verification:** Test of each unavailable-data policy during terrain loading

Flying-carpet profiles shall select an explicit unavailable-data policy, such
as hold last valid surface, continue inertially within limits, climb to a safe
clearance, or stop.

## Diagnostics and tests

### REQ-CSWU-MOVE-016: Diagnostics

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-163
- **Verification:** Test reading each listed movement diagnostic

Movement diagnostics shall expose selected model, state, input age, query age,
clearance, active constraints, fallback state, and update cost.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Movement Models](../architecture/movement-models.md#verification).

## Related Documentation

- [Movement Models requirements](../architecture/movement-models.md)
- [Movement architecture](../../design/movement-models.md)
- [Scene control](scene-control.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Camera and movement use cases](../use-cases/camera.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
