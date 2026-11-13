# Requirements: Players and Ground Clamping

## Purpose

CSWUnity shall give applications an engine-neutral SDK for Players, that is,
moving world objects that the application controls, built on the class model
of the generic CSW baseline. A Player can follow the ground. Its ground height
shall be kept up to date by level-of-detail signals from the streamed map, so
that a Player is clamped again only when the geometry under it disappears,
appears, or is replaced, and not on every frame.

## Scope

This document covers the CSWUnity realization of `CSW.Player` of
REQ-CSW-API-013, the class that manages the Players of a map, the Unity binding
of a Player, and the mechanisms for ground clamping driven by level-of-detail
signals. This document holds the Principle, Mechanism, and Class requirements
of the area. Its Behavior requirements are in
[Players and Ground Clamping Behavior](../functional/players.md).

The asynchronous spatial queries of REQ-CSWU-QUERY-001 to REQ-CSWU-QUERY-004
remain the general query service. This area specifies continuous clamping of
registered Players. External objects of the
[External Object Integration](object-integration.md) area may use these
mechanisms, as [OD-CSWU-010](#open-decisions) decides.

## Class model

### REQ-CSWU-PLAY-001: Engine-neutral Player SDK

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-009, UC-CSW-011, UC-CSW-021
- **Depends on:** REQ-CSW-API-003, REQ-CSW-API-004, REQ-CSW-API-013
- **Verification:** API review of the C# surface, plus an EditMode test that
  creates, moves, clamps, and removes a Player without a Unity object

CSWUnity shall realize the Player SDK through the engine-neutral classes
`CSW.Player` of REQ-CSW-API-013 and `CSW.PlayerManager` of REQ-CSWU-PLAY-003,
defined locally as REQ-CSW-API-003 requires until the CSW repository provides
them. The pose of a Player shall be a `CSW.Position` of REQ-CSW-API-004 and an
orientation. These classes shall expose no Unity type, no GizmoSDK type, and no
unmanaged pointer, so that the same contract can be realized in CSWUnreal.

### REQ-CSWU-PLAY-002: Ground-clamp settings and state

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-011, UC-CSW-023
- **Depends on:** REQ-CSWU-PLAY-001
- **Verification:** API review plus an EditMode test that sets and reads each
  setting and observes each state and event

A `CSW.Player` shall have the following ground-clamp settings:

- **Clamp mode:** `None`, in which the Player floats; `Ground`, in which its
  height is the ground height plus its height above the ground; or
  `GroundAligned`, in which its up direction also follows the ground normal.
- **Height above the ground:** zero by default.
- **Clamp mask:** the intersection mask of REQ-CSWU-PLAY-007.
- **Level-of-detail source:** `Visual` or `Fixed`, as REQ-CSWU-PLAY-008
  specifies.

A `CSW.Player` shall report its clamp state as resolved, provisional, or
unavailable, with the meaning of REQ-CSW-API-004, together with the last
ground point and ground normal. It shall raise the event `Clamped` when its
clamped pose changes and the event `GroundLost` when it loses a resolved
ground, as REQ-CSWU-PLAY-011 specifies.

### REQ-CSWU-PLAY-003: CSW.PlayerManager

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-021, UC-CSWU-005
- **Depends on:** REQ-CSWU-PLAY-001, REQ-CSWU-MM-001
- **Verification:** API review plus an EditMode test that registers and
  removes Players and reads the manager diagnostics

The Players of a map shall be registered with one `CSW.PlayerManager`, which
`CSW.Unity.MapControl` of REQ-CSWU-MM-001 owns for its session. The manager
shall:

- keep the set of Players that must be clamped again, called the dirty set;
- own the intersection resources and reuse them for all Players, so that a
  Player owns no native intersector;
- clamp the dirty set as REQ-CSWU-PLAY-006 specifies; and
- report per frame the number of dirty Players, intersections, cached-triangle
  evaluations, and Players that lost the ground.

Removing a Player or stopping the session shall release every subscription
and cached item of the Player.

### REQ-CSWU-PLAY-004: Unity Player binding

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-011, UC-CSWU-005
- **Depends on:** REQ-CSWU-PLAY-001, REQ-CSWU-COORD-006
- **Verification:** PlayMode test in which a component follows a clamped
  Player through terrain level-of-detail changes and an origin rebase

CSWUnity shall provide the component `CSW.Unity.Player`, which binds a
`CSW.Player` to a Unity `Transform`, as `CSW.Unity.MapControl` realizes
`CSW.MapControl`. The component shall write the pose of the Player to its
`Transform` through the viewer mapping of REQ-CSWU-COORD-006 in the same
refresh as the pose changes, and shall not be required for clamping, so that a
Player without a visual model is clamped in the same way.

## Ground clamping

### REQ-CSWU-PLAY-005: Clamp anchor from the intersection history

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-023
- **Depends on:** REQ-CSWU-QUERY-004, REQ-CSWU-SM-009, REQ-CSWU-SM-023
- **Verification:** EditMode test with a synthetic level-of-detail graph that
  checks the selected clamp anchor and that no native node is referenced after
  the clamp

A clamp shall intersect the scene graph with the GizmoSDK intersector with its
history enabled, and with the ground normal and the hit triangle requested.
From the history of the hit, the clamp shall select the nearest ancestor of the
hit node that is a level-of-detail node or a dynamically loaded node, called
the clamp anchor, and shall identify it by its occurrence identity of
REQ-CSWU-SM-023. The clamp shall not keep a pointer to a native node after the
edit access of REQ-CSWU-SM-009 in which it intersected.

### REQ-CSWU-PLAY-006: Clamping driven by level-of-detail signals

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-023
- **Depends on:** REQ-CSWU-PLAY-005, REQ-CSWU-PERF-012
- **Verification:** PlayMode test in which only the Players under a changed
  clamp anchor are clamped again, checked through the manager diagnostics

`CSW.PlayerManager` shall index its Players by clamp anchor. When a frame
commit of REQ-CSWU-PERF-012 changes the activation of a clamp anchor or of any
occurrence in its subtree, or removes the anchor, the Players of that anchor
shall enter the dirty set. This covers the anchor being hidden or unloaded, a
finer level of detail becoming active under the Player, and a map replacing the
ground. A Player shall also enter the dirty set when its clamp settings change,
when it is moved by more than its cached triangle allows as
REQ-CSWU-PLAY-009 specifies, or when it has no resolved ground. The manager
shall clamp the dirty set in one batch within one edit access, after the frame
commit has applied its activations and before the frame is rendered.

### REQ-CSWU-PLAY-007: Clamp mask

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010
- **Depends on:** REQ-CSWU-PLAY-005
- **Verification:** EditMode test in which a Player over a building is clamped
  to the terrain by default and to the roof when the mask selects buildings

A clamp shall intersect only the nodes that match the clamp mask of the
Player, through the intersection mask of the scene instances. By default, the
mask shall select the terrain, so that a Player is not clamped to buildings,
other Players, or its own visual model unless the application selects them.

### REQ-CSWU-PLAY-008: Level-of-detail source

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-011
- **Depends on:** REQ-CSWU-PLAY-005, REQ-CSWU-SM-050
- **Verification:** PlayMode test comparing the clamped height of a Player
  with each source while the camera moves away from it

A clamp shall use one of two level-of-detail sources:

- **Visual:** the levels of detail that are active for the presented camera,
  so that the Player stands on the ground that is rendered. Its height may
  change when the camera moves.
- **Fixed:** a stated level-of-detail factor that is independent of the
  camera, so that simulations and network peers obtain the same height.

The default source and whether `Fixed` is required in the first realization
are decided by [OD-CSWU-009](#open-decisions).

### REQ-CSWU-PLAY-009: Hit-triangle cache

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-011
- **Depends on:** REQ-CSWU-PLAY-005, REQ-CSWU-PLAY-006
- **Verification:** EditMode test in which a Player moving inside one ground
  triangle is clamped without an intersection and is intersected again when it
  leaves the triangle

A clamp shall cache the hit triangle and its clamp anchor for each Player.
While a moving Player stays inside the horizontal projection of its cached
triangle and its clamp anchor has not signaled a change, its ground height and
ground normal shall be computed from the cached triangle without an
intersection. When the Player leaves the triangle, it shall enter the dirty
set.

## Open Decisions

The following design choices are unresolved. A requirement that depends on an
unresolved open decision cannot become Accepted.

| ID | Decision needed | Affects |
| --- | --- | --- |
| OD-CSWU-009 | The default level-of-detail source of a Player, `Visual` or `Fixed`, and whether `Fixed` is required in the first realization. | REQ-CSWU-PLAY-002, REQ-CSWU-PLAY-008 |
| OD-CSWU-010 | Whether an external object of the External Object Integration area is presented as a `CSW.Player`, or uses the ground clamping of this area as a service through its presenter. | REQ-CSWU-PLAY-003, REQ-CSWU-OBJ-015, REQ-CSWU-OBJ-018, REQ-CSWU-OBJ-020 |

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Players and Ground Clamping Behavior](../functional/players.md), is verified
by a reference scenario with many ground-clamped Players on a streamed map
during fast camera motion, in which no Player floats or sinks in a rendered
frame and the number of intersections per frame follows the number of Players
under changed clamp anchors, not the number of Players.

## Related Documentation

- [Players and Ground Clamping Behavior requirements](../functional/players.md)
- [External Object Integration requirements](object-integration.md)
- [Scene Control requirements](../functional/scene-control.md)
- [CSW Baseline Realization requirements](csw-baseline-realization.md)
- [Generic CSW baseline requirements](../generic/requirements.md)
- [Generic CSW baseline use cases](../generic/use-cases.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
