# Requirements: Runtime Initialization Behavior

## Purpose

This document holds the Behavior requirements of the
[Runtime Initialization](../architecture/runtime-initialization.md) area. The
purpose, scope, and verification of the area are described in that document.

## Scope

The scope of the area is described in
[Runtime Initialization](../architecture/runtime-initialization.md#scope).

## Requirements

### REQ-CSWU-INIT-003: SceneManager startup

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test proving that map commands cannot reach SceneManager
  before initialization completes
- **Superseded by:**
  [REQ-CSWU-SM-011](../architecture/csw-scenemanager-integration.md#req-cswu-sm-011-session-startup-order),
  [REQ-CSWU-SM-012](../architecture/csw-scenemanager-integration.md#req-cswu-sm-012-geometry-capabilities)

CSWUnity shall:

1. create the C# CSW SceneManager;
2. register its command-buffer receiver;
3. start SceneManager processing;
4. submit initialization and capability commands; and
5. submit map commands only after initialization succeeds.

### REQ-CSWU-INIT-004: Lifecycle state

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test of lifecycle state transitions and repeated start and
  stop requests

The runtime shall expose an observable lifecycle with at least `Created`,
`Starting`, `Running`, `Stopping`, `Stopped`, and `Failed` states.

Repeated start or stop requests shall be handled deterministically.

### REQ-CSWU-INIT-005: Configuration precedence

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test of the map-list precedence, of a source that holds
  several map URLs separated by `;`, and of the structured error when no
  source supplies a map URL, plus review proving that no checkout-relative
  file is required

CSWUnity shall support serialized Unity configuration assets and explicit
runtime overrides supplied by the host application.

The map list shall be resolved in this order of precedence, highest first:

1. a map list supplied explicitly as an argument to the host API;
2. the map list configured on the host component; and
3. the `SceneManager/MapUrl` configuration value.

A string source may hold several map URLs separated by `;`, as the CSWUnreal
`MapUrls` property does. When none of these sources supplies a map URL,
startup shall fail with a structured error that names the missing sources.

Configuration precedence shall be documented and shall not require a
checkout-relative file such as `config.xml`.

### REQ-CSWU-INIT-007: Controlled shutdown

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Shutdown test proving that no callback, worker, Unity
  object, or platform reference remains
- **Superseded by:**
  [REQ-CSWU-SM-034](../architecture/csw-scenemanager-integration.md#req-cswu-sm-034-session-shutdown-order)

Shutdown shall stop accepting new work, cancel or drain queued work according
to the selected shutdown policy, release Unity realization resources, detach
the buffer receiver, stop and dispose the C# CSW SceneManager, and finally
release the GizmoSDK platform reference.

No callback shall target destroyed Unity objects after shutdown begins.

## Verification

Each requirement states its own verification method. The verification of the
area as a whole is described in
[Runtime Initialization](../architecture/runtime-initialization.md#verification).

## Related Documentation

- [Runtime Initialization requirements](../architecture/runtime-initialization.md)
- [CSW SceneManager Integration requirements](../architecture/csw-scenemanager-integration.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Runtime initialization design](../../design/runtime-initialization.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
