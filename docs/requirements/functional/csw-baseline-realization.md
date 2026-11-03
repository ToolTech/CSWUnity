# Requirements: CSW Baseline Realization Behavior

## Purpose

This document holds the Behavior requirements of the
[CSW Baseline Realization](../architecture/csw-baseline-realization.md) area.
The purpose, scope, and verification of the area are described in that document.

## Scope

The scope of the area is described in
[CSW Baseline Realization](../architecture/csw-baseline-realization.md#scope).
This document contains requirements of the following groups:

- SceneManager mechanism
- Other common capabilities

## SceneManager mechanism

### REQ-CSWU-SM-005: New, update, and delete

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-006
- **Verification:** EditMode test of new, update, and delete with identity
  registration and failure cleanup (intended fixture: `CswSceneCommandTests`)
- **Superseded by:**
  [REQ-CSWU-SM-027](../architecture/csw-scenemanager-integration.md#req-cswu-sm-027-occurrence-lifecycle)

A new command shall resolve the parent, create and build the Unity object, and
register its complete scene-instance identity. An update shall resolve and
update that object. A delete shall destroy the object and remove the identity
registration.

### REQ-CSWU-SM-006: Correlated result polling

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-007
- **Verification:** Test of correlated callback and polling results
- **Superseded by:**
  [REQ-CSWU-SM-033](../architecture/csw-scenemanager-integration.md#req-cswu-sm-033-correlated-asynchronous-requests)

CSWUnity shall preserve the command reference identity from an asynchronous
request to its response. A C# callback or event may publish the result, and a
polling operation shall return and consume the stored result for that identity.

## Other common capabilities

### REQ-CSWU-CV-001: Camera and coordinate realization

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** None
- **Depends on:** REQ-CSW-CV-001, REQ-CSW-CV-002
- **Verification:** Coordinate tests plus test of a View that uses a Camera
  other than the Main Camera

CSWUnity shall map generic GeoPosition, Global 3D, ENU, and object-local Camera
values to the selected Unity coordinate frame. A Unity View shall use its
referenced Camera even when it differs from SceneManager's Main Camera.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[CSW Baseline Realization](../architecture/csw-baseline-realization.md#verification).

## Related Documentation

- [CSW Baseline Realization requirements](../architecture/csw-baseline-realization.md)
- [CSW SceneManager Integration requirements](../architecture/csw-scenemanager-integration.md)
- [Generic CSW baseline requirements](../generic/requirements.md)
- [Generic CSW baseline use cases](../generic/use-cases.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
