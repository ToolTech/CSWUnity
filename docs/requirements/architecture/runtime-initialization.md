# Requirements: Runtime Initialization

## Purpose

CSWUnity must establish the GizmoSDK and C# CSW SceneManager runtime before
submitting map or frame commands. Initialization must be explicit, reusable,
and safe when CSWUnity is embedded in a larger Unity application.

## Scope

This document covers composition, platform and SceneManager startup, lifecycle
state, configuration, diagnostics, shutdown, and platform services of the
CSWUnity runtime. This document holds the Principle and Mechanism requirements
of the area. Its Behavior requirements are in
[Runtime Initialization Behavior](../functional/runtime-initialization.md).

[REQ-CSWU-INIT-005](../functional/runtime-initialization.md#req-cswu-init-005-configuration-precedence)
incorporates the configuration precedence decided on 2026-10-28.

## Requirements

### REQ-CSWU-INIT-001: Explicit composition

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** PlayMode test that configures and starts the runtime without
  a sample scene or scene-wide lookup

The host project shall be able to configure the CSWUnity runtime, camera,
streaming sources, realization profile, and optional features through explicit
component references and assets.

Runtime correctness shall not depend on scene-wide type searches or a
particular GameObject name or scene hierarchy.

### REQ-CSWU-INIT-002: Ordered platform startup

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test of platform initialization before SceneManager creation
  and of balanced reference counting
- **Superseded by:**
  [REQ-CSWU-SM-011](csw-scenemanager-integration.md#req-cswu-sm-011-session-startup-order),
  [REQ-CSWU-SM-034](csw-scenemanager-integration.md#req-cswu-sm-034-session-shutdown-order)

CSWUnity shall initialize the GizmoSDK platform before creating or starting a
C# CSW SceneManager.

Platform initialization shall be reference-counted or otherwise coordinated so
that multiple CSW components cannot initialize or shut down GizmoSDK out of
order.

### REQ-CSWU-INIT-006: Diagnostics integration

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test proving that diagnostics reach the Unity logging
  adapter with severity, source, and correlation information

CSWUnity shall route GizmoSDK and CSW SceneManager diagnostics to a configurable
Unity logging adapter while preserving severity, source, and correlation
information.

### REQ-CSWU-INIT-008: Unity lifecycle independence

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** Test controlling startup and shutdown explicitly and through
  the optional `OnEnable`/`OnDisable` integration

The host project shall be able to control runtime startup and shutdown
explicitly. Automatic `OnEnable`/`OnDisable` integration may be provided, but
it shall use the same public lifecycle and shall not be the only supported
mode.

### REQ-CSWU-INIT-009: Platform services

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Build review proving that unused platform services are not
  included or activated

Platform-specific services, including Android registry, network, and multicast
setup, shall be isolated behind platform adapters. Projects that do not require
a platform service shall not have to include or activate it.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Runtime Initialization Behavior](../functional/runtime-initialization.md), is
verified when the following acceptance criteria hold:

- A host project can configure and start CSWUnity without a sample scene or
  scene-wide object lookup.
- Map commands cannot reach SceneManager before platform and SceneManager
  initialization has completed.
- Startup failure produces the `Failed` state and a structured diagnostic.
- Starting or stopping twice does not leak resources or create duplicate
  workers.
- Explicit shutdown leaves no active SceneManager callback, worker, Unity
  object, or GizmoSDK platform reference owned by the stopped runtime.

## Related Documentation

- [Runtime Initialization Behavior requirements](../functional/runtime-initialization.md)
- [CSW SceneManager Integration requirements](csw-scenemanager-integration.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Runtime initialization design](../../design/runtime-initialization.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
