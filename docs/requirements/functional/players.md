# Requirements: Players and Ground Clamping Behavior

## Purpose

This document holds the Behavior requirements of the
[Players and Ground Clamping](../architecture/players.md) area. The purpose,
scope, and verification of the area are described in that document.

## Scope

The scope of the area is described in
[Players and Ground Clamping](../architecture/players.md#scope). This document
contains requirements of the following group:

- Ground clamping

## Ground clamping

### REQ-CSWU-PLAY-010: Clamp in the same frame

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-023
- **Depends on:** REQ-CSWU-PLAY-006
- **Verification:** PlayMode test in which a frame commit replaces the level
  of detail under a ground-clamped Player and the rendered frame of that commit
  shows the Player on the new ground

When a frame commit changes the ground under a ground-clamped Player, the
Player shall have its new clamped pose in the frame that the commit presents,
so that no rendered frame shows the Player above or below the presented
ground.

### REQ-CSWU-PLAY-011: Missing ground without blocking

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-010, UC-CSW-023
- **Depends on:** REQ-CSWU-PLAY-002, REQ-CSWU-PLAY-006
- **Verification:** PlayMode test that unloads and reloads the ground under a
  Player and checks its states, events, and pose, and that the Unity main
  thread never waits for dynamic data

A clamp on the Unity main thread shall never wait for dynamic data to load.
When a clamp finds no ground, the Player shall keep its last pose. Its clamp
state shall become provisional if it had a resolved ground and unavailable
otherwise, it shall raise `GroundLost` once, and it shall stay in the dirty set.
When ground is found again, its state shall become resolved, and it shall raise
`Clamped`.

### REQ-CSWU-PLAY-012: No redundant intersections

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-011, UC-CSW-021
- **Depends on:** REQ-CSWU-PLAY-003, REQ-CSWU-PLAY-006, REQ-CSWU-PLAY-009
- **Verification:** PlayMode test of stationary and moving Players during
  streaming, checked through the manager diagnostics

A ground-clamped Player whose clamp anchor has not signaled a change shall
cause no intersection while it stands still or moves inside its cached
triangle. The number of intersections in a frame shall not grow with the number
of such Players.

### REQ-CSWU-PLAY-013: Removal during a clamp

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSWU-005
- **Depends on:** REQ-CSWU-PLAY-003
- **Verification:** PlayMode test that removes Players and stops the session
  while Players are in the dirty set

A Player that is removed, or whose session stops, shall receive no further
clamp, event, or pose update, and the manager shall hold no reference to it
after the removal returns.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Players and Ground Clamping](../architecture/players.md#verification).

## Related Documentation

- [Players and Ground Clamping requirements](../architecture/players.md)
- [External Object Integration Behavior requirements](object-integration.md)
- [Scene Control requirements](scene-control.md)
- [Generic CSW baseline use cases](../generic/use-cases.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
