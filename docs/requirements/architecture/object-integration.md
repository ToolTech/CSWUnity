# Requirements: External Object Integration

## Purpose

CSWUnity shall expose an engine-neutral object interface that makes it
straightforward for a host project to connect updates from CSW Distribution,
HLA, MCP, future DIS support, recorded scenarios, or product-specific systems.

Transport adapters and Unity presenters shall remain independent.

## Scope

This document covers the core object contract, the feed, store, and adapter
boundaries, ordering and motion, and Unity presentation of external world
objects. This document holds the Principle, Mechanism, and Class requirements of
the area. Its Behavior requirements are in
[External Object Integration Behavior](../functional/object-integration.md).

## Core object contract

### REQ-CSWU-OBJ-001: Stable identity

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-005, UC-CSWU-008
- **Verification:** EditMode test of stable object identity across namespaces
  and value updates

Every external object shall have a stable identity containing an identity
namespace and value. Unity instance IDs and transport-local pointers shall not
be public object identities.

### REQ-CSWU-OBJ-002: Type and schema

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Schema review plus test rejecting incompatible schema
  versions

Every object shall identify a versioned type/schema. Schemas shall define
attribute names, value types, units, optionality, coordinate requirements, and
compatibility policy.

### REQ-CSWU-OBJ-003: Change types

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-005
- **Verification:** EditMode test applying each object change type

The object contract shall distinguish:

- create;
- complete snapshot;
- partial attribute update;
- event;
- remove; and
- authority or ownership change.

### REQ-CSWU-OBJ-004: Dynamic values

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** None
- **Verification:** Test of typed dynamic attributes without transport encoding

The contract shall support typed dynamic attributes compatible with CSW and
GizmoSDK value semantics without forcing transport-specific encoding on
consumers.

### REQ-CSWU-OBJ-005: Time and revision

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Test of object time and revision fields

Every change shall carry available source time, receive time, source sequence
or revision, and correlation identity.

### REQ-CSWU-OBJ-006: Coordinate frame

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Test of the declared frame, axis, unit, and generation
  information

Position, orientation, velocity, acceleration, and angular values shall declare
their coordinate frame, axis convention, and SI units. A Local 3D value shall
also declare its Local 3D Frame identity and generation. Positions shall use a
`GeoPosition` with its CRS context, a Global 3D Position with its Map Coordinate
Context, or a Local 3D Position with its complete frame context.

### REQ-CSWU-OBJ-007: Authority

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Authority-conflict test with each explicit authority policy

The object service shall track update authority at object or attribute-group
level. Conflict resolution, leases/epochs, priorities, and write permission
shall be explicit policies.

## Feed, store, and adapter boundaries

### REQ-CSWU-OBJ-008: Transport-neutral feed

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Assembly review proving no `UnityEngine` dependency plus a
  feed test

Transport adapters shall publish normalized object changes through an
engine-neutral feed with no `UnityEngine` dependency.

### REQ-CSWU-OBJ-009: Object store

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Test of object-store validation and ordering

An ordered object store shall validate lifecycle, schema, authority, revision,
and time before publishing approved changes to consumers.

### REQ-CSWU-OBJ-010: Transport adapters

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M4
- **Use cases:** UC-CSWU-008, UC-CSWU-013
- **Verification:** Equivalent object scenarios delivered through two transport
  adapters

The architecture shall support separate adapters for CSW Distribution, HLA,
MCP, recorded data, and future protocols. Adding an adapter shall not require
changes to Unity presenters.

### REQ-CSWU-OBJ-011: Bounded ingestion

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M4
- **Use cases:** UC-CSWU-016
- **Verification:** Overload test observing queue depth, drops, reconnects, and
  resynchronization

Each external source shall use bounded queues or an explicit backpressure and
loss policy. Queue depth, dropped updates, reconnects, and resynchronization
shall be observable.

## Ordering and motion

### REQ-CSWU-OBJ-015: Motion presentation

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M4
- **Use cases:** UC-CSWU-005
- **Verification:** Interpolation, extrapolation, stale-state, and correction
  tests

Timestamped motion state shall support configurable interpolation, bounded
extrapolation, stale-state handling, and correction smoothing.

Motion estimation shall be a presentation service, not transport-specific
logic.

## Unity presentation

### REQ-CSWU-OBJ-017: Presenter registry

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M4
- **Use cases:** UC-CSWU-005, UC-CSWU-009
- **Verification:** Test of deterministic presenter selection

CSWUnity shall select a presenter through a deterministic registry based on
object schema and declared capabilities.

### REQ-CSWU-OBJ-018: Presenter lifecycle

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M4
- **Use cases:** UC-CSWU-005, UC-CSWU-009
- **Verification:** Test of each presenter lifecycle operation

A presenter shall support create, snapshot, delta, event, visibility, stale,
authority, and remove behavior as applicable.

### REQ-CSWU-OBJ-019: Main-thread boundary

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M4
- **Use cases:** UC-CSWU-008
- **Verification:** Test proving that transport callbacks perform no Unity
  access

Transport callbacks and object-store dispatch shall not mutate Unity objects.
Approved immutable state shall be applied through a bounded Unity main-thread
stage.

### REQ-CSWU-OBJ-020: Map alignment

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M4
- **Use cases:** None
- **Verification:** Map-alignment test of a moving object on UTM, geodetic, and
  geocentric maps

External objects shall use the same world, Map Coordinate Context, and Local 3D
Frame identity and generation as streamed map content.

### REQ-CSWU-OBJ-021: Host extension

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M4
- **Use cases:** UC-CSWU-009
- **Verification:** Test registering a host object type, schema, presenter, and
  handler

A host project shall be able to register product-specific object types,
schemas, presenters, and event handlers without forking CSWUnity.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[External Object Integration Behavior](../functional/object-integration.md), is
verified when the following acceptance criteria hold:

- Equivalent object scenarios delivered through two adapters produce the same
  approved object-store state and Unity presentation.
- Create, partial update, event, remove, duplicate, out-of-order, reconnect,
  and authority-conflict cases have deterministic outcomes.
- A moving object remains aligned with UTM, geodetic, and geocentric maps.
- Transport callbacks perform no Unity access.
- Removal during queued interpolation or resource construction cannot publish
  a stale Unity object.

## Related Documentation

- [External Object Integration Behavior requirements](../functional/object-integration.md)
- [External object architecture](../../design/object-integration.md)
- [Scene control and threading](../../design/scene-control-and-threading.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
