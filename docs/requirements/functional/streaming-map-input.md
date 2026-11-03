# Requirements: Streaming Map Input

## Purpose

CSWUnity consumes streamed scene data through the C# CSW SceneManager and
realizes that data in Unity. GZD is the first required map format; the
CSWUnity-facing scene contract must not prevent additional streamed formats.

## Scope

This document covers how CSWUnity consumes streamed maps through the C# CSW
SceneManager: GZD input, format ownership, map addressing and lifecycle, Unity
scene realization, coordinate adaptation, dynamic streaming, failure reporting,
and version compatibility. This document holds the Behavior requirements of the
area. Its Principle requirements are in
[Streaming Map Input Architecture](../architecture/streaming-map-input.md).

## Requirements

### REQ-CSWU-MAP-001: GZD map input

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** PlayMode test with a published GZD map from the supported
  CSWMapGenerator version (intended fixture: `CswNativeMapTests`)

CSWUnity shall load GZD maps generated and published by a supported version of
CSWMapGenerator through the C# CSW SceneManager and GizmoSDK.

### REQ-CSWU-MAP-004: Map addressing

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test loading each supported URL form plus review proving
  that scene assets embed no credentials

CSWUnity shall accept map URLs supported by the configured CSW SceneManager and
GizmoSDK serialization adapters. Credentials and transport-specific settings
shall not be embedded in scene assets.

### REQ-CSWU-MAP-005: Map lifecycle

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-039
- **Verification:** PlayMode test of set, replace, and clear of a map list
  with completion and failure reporting for each map

CSWUnity shall support asynchronous set, replace, and clear operations for the
map list of REQ-CSWU-SM-039 and expose their completion or failure for each
map. Several active maps and their coordinate-system compatibility are
REQ-CSWU-MAPCTRL-004.

### REQ-CSWU-MAP-006: Unity scene realization

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** EditMode test of ordered structural and activation changes
  (intended fixture: `CswSceneCommandTests`) plus PlayMode map test (intended
  fixture: `CswNativeMapTests`)

CSWUnity shall process structural `New`, `Update`, `Delete`, and `Activation`
changes emitted by CSW SceneManager and realize supported content through a
configurable builder/factory registry.

### REQ-CSWU-MAP-007: Coordinate adaptation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-002
- **Verification:** Rebase test of stable Global 3D Positions and frame identity
  under the Unity Viewer Transform

CSWUnity shall preserve the Map Coordinate Context, the Map Origin
`GeoPosition` when georeferenced, Global 3D Positions, and Local 3D Frame
identity and generation while applying the current Unity Viewer Transform.

### REQ-CSWU-MAP-008: Dynamic streaming

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** PlayMode test with a published GZD map proving that camera
  movement updates streamed content and LOD (intended fixture:
  `CswNativeMapTests`)

CSWUnity shall submit camera and frame information to CSW SceneManager so that
GizmoSDK can update dynamic content and levels of detail without loading the
complete map into Unity.

### REQ-CSWU-MAP-009: Load failure reporting

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test of distinct results for invalid, unavailable,
  incompatible, and cancelled sources

Map and transport failures shall be returned as structured, correlated
diagnostics. Retry, replacement URL, and cancellation policy shall be owned by
the host application or an explicitly configured CSWUnity policy.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Streaming Map Input Architecture](../architecture/streaming-map-input.md), is
verified when the following acceptance criteria hold:

- A published GZD map produced by the supported CSWMapGenerator version loads
  through the C# CSW SceneManager.
- Frame buffers produce correctly ordered Unity `New`, `Update`, `Delete`, and
  `Activation` results.
- Camera movement updates streamed content and levels of detail.
- Global 3D Positions remain stable across Local Origin Offset rebases.
- Invalid, unavailable, incompatible, and cancelled sources produce distinct
  observable results.
- A test format provider can emit the common scene contract without requiring
  changes to Unity builders that support its payload types.

## Related Documentation

- [Streaming Map Input Architecture requirements](../architecture/streaming-map-input.md)
- [Streaming Map pipeline](../../design/streaming-map-pipeline.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
- [CSW architecture and SceneManager](https://saab.ghe.com/saab/CSW)
- [CSWMapGenerator](https://saab.ghe.com/saab/CSWMapGenerator)
